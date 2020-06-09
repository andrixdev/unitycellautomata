using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
//using UnityEngine.Experimental.VFX;
using UnityEngine.VFX;

public enum Kinds
{
	BTH_A, //birth and alive
	BTH_D, //birth and dead
	STN_A, //sustain and alive
	STN_D, //sustain and dead
	DTH_A, //deathzone and alive
	DTH_D  //deathzone and dead
}

// Note: GameObject with this script needs to also have a VisualEffect component
public class LifeSim : MonoBehaviour
{
	// Cell Automata mechanics concerns

	private const byte Moore = 0;
	private const byte VNeumann = 1;

	private const byte DEAD = 0;
	private const byte ALIVE = 1;

	public int nbrOfCells = 30;

	// Containers
	private Cell[,,] cellGrid;
	public byte[,,] nextStatusGrid;

	private int step = 0;
	public int stepInterval = 1;
	public int popRefreshRate = 200;
	//private float timer;
	//public float pauseDuration;

	private int population = 0;

	// Automaton parameters
	public string ruleKey = "9-20/5-7,12-13,15-16/5/M";
	private string oldRuleKey;
	private byte neighbour;
	private byte[][] rule;

	// Lotka-Volterra model for cellular automata
	public double lambda = 2.3;
	private int deltaPop;
	private double pAdd, pMinus;
	private double alpha, beta;
	private double p, deltap;

	// Partition of cells per neighbourhood and status

	private int birthAlive = 0;
	private int birthDead = 0;

	private int sustainAlive = 0;
	private int sustainDead = 0;

	private int deathzoneAlive = 0;
	private int deathzoneDead = 0;

	// Concerns for sampling

	private bool[] visited;
	private bool[] visitedAsNeighbour;

	private List<int> samples = new List<int>();
	private List<int> neighbourSamples = new List<int>();

	private int sampling;

	// For the quasirandom sequence
	private double[] pos;
	private static double g = 1.22074408460575947536;
	private static double a0 = 1 / g;
	private static double a1 = 1 / (g * g);
	private static double a2 = 1 / (g * g * g);
	

	//Initial conditions 
	public double spawnRate = 0.05;
	public int lifeRange = 50;
	public bool fillInit = false;
	public bool centerInit = false;

	// Visualization concerns
	private Texture2D positionTexture; // Texture to store positions
    private Texture2D statusTexture; // Texture to store statuses
	private VisualEffect VFX;
	
	// Interaction with outter world
	public Transform interactor;
	public float visualScale = 5;

	// Trace
	private string log_addr = System.IO.Directory.GetCurrentDirectory() + "/log.txt";
	private string data_addr = System.IO.Directory.GetCurrentDirectory() + "/log.csv";
	private System.IO.StreamWriter log_file;
	private System.IO.StreamWriter data_file;

	// MonoBehaviour Start() called once
	public void Start()
	{
		this.StartAutomaton();
		this.StartVisualsInjection();
		UnityEngine.Debug.Log(this.ruleKey);
		System.IO.File.WriteAllText(log_addr, "Log\nRule key : " + this.ruleKey +"\nSize : " + nbrOfCells* nbrOfCells* nbrOfCells +"\nLambda : " + lambda + "\n");
		System.IO.File.WriteAllText(data_addr, "step , population , deltap\n");
		log_file = new System.IO.StreamWriter(log_addr, true);
		data_file = new System.IO.StreamWriter(data_addr, true);
	}
	
	// MonoBehaviour Update() called each frame
	public void Update()
	{
		// Update step (quick&dirty way)
		step++;
		if (!this.oldRuleKey.Equals(this.ruleKey))
			this.translateRule();
		
		if (step % (popRefreshRate*stepInterval) == 0)
        {
			log_file.WriteLine(String.Format("\n------ Step {0} -----", step));
			UnityEngine.Debug.Log(String.Format("------ Step {0} -----", step));

			this.parametersUpdate();

			log_file.WriteLine(String.Format("Pop : {0}", this.population));
			UnityEngine.Debug.Log(String.Format("Pop : {0}", this.population));

			log_file.WriteLine(String.Format("Delta Pop : {0}", this.deltaPop));
			UnityEngine.Debug.Log(String.Format("Delta Pop : {0}", this.deltaPop));

			
			log_file.WriteLine(String.Format("\npAdd : {0}", this.pAdd));

			log_file.WriteLine(String.Format("Birth Dead : {0}", this.birthDead));
			log_file.WriteLine(String.Format("Sustain Dead : {0}", this.sustainDead));
			log_file.WriteLine(String.Format("Deathzone Dead : {0}", this.deathzoneDead));
			
			log_file.WriteLine(String.Format("\npMinus: {0}", this.pMinus));

			log_file.WriteLine(String.Format("Birth Alive : {0}", this.birthAlive));
			log_file.WriteLine(String.Format("Sustain Alive : {0}", this.sustainAlive));
			log_file.WriteLine(String.Format("Deathzone Alive : {0}", this.deathzoneAlive));

			data_file.WriteLine(step.ToString() + " , " + p.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + " , " + deltap.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
		}


		if (step % stepInterval == 0)
		{
			// Probabilistic method
			this.sampleUpdate();
		}
		this.UpdatesForEachFrame();
	}
	
	public void StartAutomaton()
	{
		// Init rule array (27x2) -> Change to 27... we can have between 0 and 26 alive neighours, so 27 possibilities 
		this.rule = new byte[27][]; // 27 values
		for (int i = 0; i < 27; i++)
        {
			this.rule[i] = new byte[2];
        }

		Cell.lifeRange = this.lifeRange;
		visited = new bool[nbrOfCells * nbrOfCells * nbrOfCells];
		visitedAsNeighbour = new bool[nbrOfCells * nbrOfCells * nbrOfCells];
		pos = new double[3];
		pos[0] = 0.5;
		pos[1] = 0.5;
		pos[2] = 0.5;

		// Init cellGrid and nextStatusGrid
		this.cellGrid = new Cell[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		this.nextStatusGrid = new byte[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		byte initCellStatus;
		
		for (int i = 0; i < this.nbrOfCells; i++)
        {
			for (int j = 0; j < this.nbrOfCells; j++)
            {
				for (int k = 0; k < this.nbrOfCells; k++)
                {
					// Cell random initial status
					if (this.centerInit)
                    {
						if (i >= this.nbrOfCells/2 - 3 && i <= this.nbrOfCells/2 + 3 
							&& j >= this.nbrOfCells / 2 - 3 && j <= this.nbrOfCells / 2 + 3 
							&& k >= this.nbrOfCells / 2 - 3 && k <= this.nbrOfCells / 2 + 3)
                        {
							if (this.fillInit)
							{
								initCellStatus = UnityEngine.Random.value < this.spawnRate ? DEAD : ALIVE;
							}

                            else
                            {
								initCellStatus = UnityEngine.Random.value < this.spawnRate ? ALIVE : DEAD;
							}
								
                        }
                        else
                        {
							initCellStatus = DEAD;
                        }
                    }
                    else
                    {
						if (this.fillInit)
						{
							initCellStatus = UnityEngine.Random.value < this.spawnRate ? DEAD : ALIVE;
						}

						else
						{
							initCellStatus = UnityEngine.Random.value < this.spawnRate ? ALIVE : DEAD;
						}
					}

					if (initCellStatus == ALIVE)
						this.population++;

					this.cellGrid[i,j,k] = new Cell(i, j, k,initCellStatus);
					
					// Next status grid initial value
					this.nextStatusGrid[i,j,k] = initCellStatus;
                }
            }
        }
		
		this.oldRuleKey = (string) this.ruleKey.Clone();
		this.translateRule();
		this.recountKinds();
	}
	
	public void StartVisualsInjection()
	{
		// Build textures
        CreateTextures(nbrOfCells);
		
		// Feed le VFX
		VFX = (VisualEffect) GetComponent<VisualEffect>();
		VFX.SetTexture("PositionTexture", positionTexture);
		VFX.SetTexture("StatusTexture", statusTexture);
		VFX.SetInt("LifeRange", this.lifeRange);
	}
	
	private void translateRule()
	{ // Transforme la chaine ruleKey en une rule utilisable
	  // Il y a 27*2 cas (de 0 à 27 voisins vivants, selon son propre status) qui doivent être couverts par une règle au pire
	  // (Il y a plus de voisins dans un voisinage de Moore que dans un voisinage de Von Neumann)

		//Initialisation du tableau :
		for (int i = 0; i < 27; i++)
        {
			this.rule[i][0] = DEAD;
			this.rule[i][1] = DEAD;
        }

		string[] splittedRule = this.ruleKey.Split('/');
		string[] sustains = splittedRule[0].Split(',');
		string[] births = splittedRule[1].Split(',');
		string[] overpops = splittedRule[2].Split(',');
		string neighbourKind = splittedRule[3];

		string[] aRange;
		int a, b;

		for(int i = 0; i < sustains.Length; i++)
        {
			
			if (sustains[i].Contains("-"))
            {
				aRange = sustains[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
			
				for(int j = a; j <= b; j++)
                {
					this.rule[j][1] = ALIVE;
                }
            }
            else
            {
				a = Int32.Parse(sustains[i]);
				this.rule[a][1] = ALIVE;
            }
        }

		for (int i = 0; i < births.Length; i++)
		{
			if (births[i].Contains("-"))
			{
				aRange = births[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = a; j <= b; j++)
				{
					this.rule[j][0] = ALIVE;
				}
			}
			else
			{
				a = Int32.Parse(births[i]);
				this.rule[a][0] = ALIVE;
			}
		}

		for (int i = 0; i < overpops.Length; i++)
        {
			if (overpops[i].Contains("-"))
            {
				aRange = overpops[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = a; j <= b; j++)
                {
					this.rule[j][1] = DEAD;
                }
            }
            else
            {
				a = Int32.Parse(overpops[i]);
				this.rule[a][1] = DEAD;
            }
        }

		if (neighbourKind.Equals("M"))
        {
			this.neighbour = Moore;
        }
        else if (neighbourKind.Equals("VN"))
        {
			this.neighbour = VNeumann;
        }
        else
        {
			throw new System.ArgumentException(neighbourKind + " est inconnu");
        }

		return;
	}

	private int countMooreNeighbours(int x, int y, int z)
	{ //Compte le nombre de voisins vivants selon le voisiange de Moore (un cube)
		int count = 0;
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				for (int k = -1; k <= 1; k++)
				{
					if ((x + i >= 0 && x + i < this.nbrOfCells) && (y + j >= 0 && y + j < this.nbrOfCells) && (z + k >= 0 && z + k < this.nbrOfCells)
							&& !(i == 0 && j == 0 && k == 0))
						{
							count += this.cellGrid[x + i,y + j,z + k].getStatus();
						}
				}
			}
		}
		return count;
	}

	private int countVonNeumannNeighbours(int x, int y, int z)
	{ //Compte le nombre de voisins vivants selon le voisinage de Von Neumann (une croix)
		int count = 0;
		count += this.cellGrid[(x + 1) % this.nbrOfCells,y,z].getStatus();
		if (x == 0)
			count += this.cellGrid[this.nbrOfCells - 1,y,z].getStatus();
			

		count += this.cellGrid[x,(y + 1) % this.nbrOfCells,z].getStatus();
		if (y == 0)
			count += this.cellGrid[x,this.nbrOfCells - 1,z].getStatus();
			

		count += this.cellGrid[x,y,(z + 1) % this.nbrOfCells].getStatus();
		if (z == 0)
			count += this.cellGrid[x,y,this.nbrOfCells - 1].getStatus();

		return count;
	}

	private void UpdatesForEachFrame()
	{
		// Update visual scale for correspondance between real position and automata [0,1] coordinate system
		// Note: there is also a position XYZ offset handled directly with a VFX Parameter Binder component on the VFX GameObject
		VFX.SetFloat("visualScale", this.visualScale);
	}
	
	private int[][] getIndexesForCubeShape(int x, int y, int z, int cubeSize)
	{
		int n = 0;// Number of valid neighbours
		// Have to specify size of array before filling it... sucks
		for (int a = x-1; a <= x+1; a++) {
			for (int b = y-1; b <= y+1; b++) {
				for (int c = z-1; c <= z+1; c++) {
					if (isInsideCube(a, b, c, cubeSize)) n++;
				}
			}
		}
		
		// Now init empty array
		int[][] indexesArray = new int[n][];
		for (int i = 0; i < n; i++) {
			indexesArray[i] = new int[3];
        }
		// And do things for real
		int m = 0; // Valid neighbour count, == current array index
		for (int a = x-1; a <= x+1; a++) {
			for (int b = y-1; b <= y+1; b++) {
				for (int c = z-1; c <= z+1; c++) {
					if (isInsideCube(a, b, c, cubeSize)) {
						indexesArray[m][0] = a;
						indexesArray[m][1] = b;
						indexesArray[m][2] = c;
						m++;
					}
				}
			}
		}
		
		return indexesArray;
	}
	
	private bool isInsideCube(int x, int y, int z, int cubeSize)
	{
		return x >= 0 && x < cubeSize && y >= 0 && y < cubeSize && z >= 0 && z < cubeSize;
	}
	
	// Create first Color texture with Positions (XYZ) and other textures with any variables
	void CreateTextures(int size)
	{
		Color[] posColorArray = new Color[size * size * size];
		Color[] statusColorArray = new Color[size * size * size];
		
		positionTexture = new Texture2D(size * size, size, TextureFormat.RGBAFloat, true);
		statusTexture = new Texture2D(size * size, size, TextureFormat.RGBAFloat, true);

		int index;

		// Single loop to inject data array values in textures in one shot
		float r = 1.0f / (size - 1.0f);
        for (int x = 0; x < size; x++) {
			for (int y = 0; y < size; y++) {
				for (int z = 0; z < size; z++) {
					index = encode(x,y,z);
					
					// Shape color information for textures1
					// Position
					Color c1 = new Color(r * x, r * y, r * z, 0);
					
					// Status (all deded)
					
					// Just to keep in mind that we store status in Color.r component
					// And lifespan in Color.g
					Color c2 = new Color(DEAD, 0, 0, 0);
					
					posColorArray[index] = c1;
					statusColorArray[index] = c2;
				}
			}
        }
		
		// Inject in textures
		positionTexture.SetPixels(posColorArray);
		statusTexture.SetPixels(statusColorArray);
		positionTexture.Apply();
		statusTexture.Apply();
	}
	
	private int encode(int x, int y, int z)
    {
		return x + y * nbrOfCells + z * nbrOfCells * nbrOfCells;
	}
	
	private int[] decode(int c)
    {
		int[] coords = new int[3];
		coords[0] = c % nbrOfCells;
		coords[1] = (c % (nbrOfCells * nbrOfCells) - coords[0])/nbrOfCells;
		coords[2] = (c - coords[0] - coords[1] * nbrOfCells) / (nbrOfCells * nbrOfCells);
		return coords;
    }

	private List<int> getNeighbourhood(int x, int y, int z)
    {
		List<int> output = new List<int>();
		for(int i = -1; i<= 1; i++)
        {
			for(int j = -1; j<=1; j++)
            {
				for(int k = -1; k<=1; k++)
                {
					if (neighbour == Moore)
                    {
						output.Add(encode(
						((x + i) % nbrOfCells + nbrOfCells) % nbrOfCells,
						((y + j) % nbrOfCells + nbrOfCells) % nbrOfCells,
						((z + k) % nbrOfCells + nbrOfCells) % nbrOfCells
						));
					}
                    else
                    {
						if(i == 0 || j == 0 || k == 0)
                        {
							output.Add(encode(
							((x + i) % nbrOfCells + nbrOfCells) % nbrOfCells,
							((y + j) % nbrOfCells + nbrOfCells) % nbrOfCells,
							((z + k) % nbrOfCells + nbrOfCells) % nbrOfCells
							));
						}
                    }
					
                }
            }
        }
		return output;
    }

	private List<int> getNeighbourhood(int c)
    {
		int[] coords = decode(c);
		return getNeighbourhood(coords[0], coords[1], coords[2]);
    }

	private Kinds getKind(int x, int y, int z)
    {
		int stat,count;
		stat = this.cellGrid[x, y, z].getStatus();
		if (neighbour == VNeumann)
        {
			count = countVonNeumannNeighbours(x, y, z);
        }
        else if (neighbour == Moore)
        {
			count = countMooreNeighbours(x, y, z);
        }
        else
        {
			count = 0;
        }

		if(rule[count][DEAD] == ALIVE)
        {
			if (stat == ALIVE)
				return Kinds.BTH_A;
			else
				return Kinds.BTH_D;
        }
		else if(rule[count][ALIVE] == ALIVE)
        {
			if (stat == ALIVE)
				return Kinds.STN_A;
			else
				return Kinds.STN_D;
        }
        else
        {
			if (stat == ALIVE)
				return Kinds.DTH_A;
			else
				return Kinds.DTH_D;
        }
    } 

	private Kinds getKind(int c)
    {
		int[] coords = decode(c);
		return getKind(coords[0], coords[1], coords[2]);
    }

	private void recountKinds()
    {
		population = 0;

		birthAlive = 0;
		birthDead = 0;

		sustainAlive = 0;
		sustainDead = 0;

		deathzoneAlive = 0;
		deathzoneDead = 0;

		for(int i = 0; i< nbrOfCells; i++)
        {
			for(int j = 0; j< nbrOfCells; j++)
            {
				for(int k = 0; k< nbrOfCells; k++)
                {
					if (cellGrid[i, j, k].getStatus() == ALIVE)
						population++;

					switch (getKind(i, j, k))
                    {
						case Kinds.BTH_A:
							birthAlive++;
							break;
						case Kinds.BTH_D:
							birthDead++;
							break;
						case Kinds.STN_A:
							sustainAlive++;
							break;
						case Kinds.STN_D:
							sustainDead++;
							break;
						case Kinds.DTH_A:
							deathzoneAlive++;
							break;
						case Kinds.DTH_D:
							deathzoneDead++;
							break;
						default:
							break;
                    }
                }
            }
        }

    }

	private void fullReconstruct()
    {
		recountKinds();
		for(int i = 0; i < nbrOfCells; i++)
        {
			for(int j = 0; j<nbrOfCells; j++)
            {
				for(int k = 0; k<nbrOfCells; k++)
                {
					statusTexture.SetPixel(i + nbrOfCells * j, k, new Color(
											cellGrid[i,j,k].getStatus(), 
											cellGrid[i,j,k].getLifespan()
											, 0, 0));
				}
            }
        }
		statusTexture.Apply();
    }

	//Quasirandom sequence R2
	//Cf http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/

	private void nextPos()
    {
		pos[0] = (pos[0] + a0);
		if (pos[0] >= 1)
			pos[0] = pos[0] - Math.Truncate(pos[0]);

		pos[1] = (pos[1] + a1);
		if (pos[1] >= 1)
			pos[1] = pos[1] - Math.Truncate(pos[1]);

		pos[2] = (pos[2] + a2);
		if (pos[2] >= 1)
			pos[2] = pos[2] - Math.Truncate(pos[2]);
	}

	private void resetPos()
    {
		pos[0] = 0.5;
		pos[1] = 0.5;
		pos[2] = 0.5;
    }

	private double[] nthPos(int n)
    {
		double[] output = new double[3];
		output[0] = (0.5 + a0 * n);
		output[0] = output[0] - Math.Truncate(output[0]);

		output[1] = (0.5 + a1 * n);
		output[1] = output[1] - Math.Truncate(output[1]);

		output[2] = (0.5 + a2 * n);
		output[2] = output[2] - Math.Truncate(output[2]);

		return output;
	}

	// Sampling to split CPU load

	private void sampleUpdate()
    {
		samples.Clear();
		neighbourSamples.Clear();
		int c;
		int t = 0;
		sampling = (nbrOfCells*nbrOfCells*nbrOfCells) / popRefreshRate;
        //Semi random - sampling 

        while (t < sampling)
        {
			c = encode((int)(Math.Truncate(pos[0] * nbrOfCells)),
						(int)(Math.Truncate(pos[1] * nbrOfCells)),
						(int)(Math.Truncate(pos[2] * nbrOfCells)));

			if (!visited[c])
            {
				visited[c] = true;
				samples.Add(c);
				neighbourSamples.AddRange(getNeighbourhood(c));
            }
			this.nextPos();
			t++;
        }
		
		//Removing cells which are going to change neighbourhood
		foreach (int n in neighbourSamples)
        {
            if (!visitedAsNeighbour[n])
            {
				visitedAsNeighbour[n] = true;
				switch (getKind(n))
                {
					case Kinds.BTH_A:
						birthAlive--;
						break;
					case Kinds.BTH_D:
						birthDead--;
						break;
					case Kinds.DTH_A:
						deathzoneAlive--;
						break;
					case Kinds.DTH_D:
						deathzoneDead--;
						break;
					case Kinds.STN_A:
						sustainAlive--;
						break;
					case Kinds.STN_D:
						sustainDead--;
						break;
					default:
						break;
                }
            }
        }

		//Build the next state from sample
		List<byte> tempStates = new List<byte>();
		int[] coords;
		if(deltaPop > 0)
        {
			foreach(int n in samples)
            {
				coords = decode(n);
				if(cellGrid[coords[0],coords[1],coords[2]].getStatus() == DEAD)
                {
					tempStates.Add(UnityEngine.Random.value < pAdd ? ALIVE : DEAD);
                }
                else
                {
					tempStates.Add(ALIVE);
				}
            }
        }
        else
        {
			foreach (int n in samples)
			{
				coords = decode(n);
				if (cellGrid[coords[0], coords[1], coords[2]].getStatus() == ALIVE)
				{
					tempStates.Add(UnityEngine.Random.value < pMinus ? DEAD : ALIVE);
                }
                else
                {
					tempStates.Add(DEAD);
				}
			}
		}
		

		//Apply the new state
		IEnumerator<int> cellsEnum = samples.GetEnumerator();
		IEnumerator<byte> statesEnum = tempStates.GetEnumerator();

		do
		{
			coords = decode(cellsEnum.Current);

			if (cellGrid[coords[0], coords[1], coords[2]].getStatus() == ALIVE && statesEnum.Current == DEAD)
				population--;
				
			else if (cellGrid[coords[0], coords[1], coords[2]].getStatus() == DEAD && statesEnum.Current == ALIVE)
				population++;

			cellGrid[coords[0], coords[1], coords[2]].changeStatus((statesEnum.Current));

			byte newCellStatus = cellGrid[coords[0], coords[1], coords[2]].getStatus();
			int newCellLifespan = cellGrid[coords[0], coords[1], coords[2]].getLifespan();

			statusTexture.SetPixel(coords[0] + nbrOfCells * coords[1], coords[2], new Color(newCellStatus, newCellLifespan, 0, 0));
			statesEnum.MoveNext();
		} while (cellsEnum.MoveNext());

		//Actualize kinds cardinals
		foreach (int n in neighbourSamples)
		{
			if (visitedAsNeighbour[n])
			{
				visitedAsNeighbour[n] = false;
				switch (getKind(n))
				{
					case Kinds.BTH_A:
						birthAlive++;
						break;
					case Kinds.BTH_D:
						birthDead++;
						break;
					case Kinds.DTH_A:
						deathzoneAlive++;
						break;
					case Kinds.DTH_D:
						deathzoneDead++;
						break;
					case Kinds.STN_A:
						sustainAlive++;
						break;
					case Kinds.STN_D:
						sustainDead++;
						break;
					default:
						break;
				}
			}
		}

		// Check for injector presence within the cube volume (0.5³ offset in VFX for centered scale)
		Vector3 localPos = 0.5f * Vector3.one + 1 / visualScale * interactor.transform.position;

		int xx = (int)Mathf.Floor(localPos.x * this.nbrOfCells);
		int yy = (int)Mathf.Floor(localPos.y * this.nbrOfCells);
		int zz = (int)Mathf.Floor(localPos.z * this.nbrOfCells);

		int cx, cy, cz;

		if (isInsideCube(xx, yy, zz, nbrOfCells))
        {
			for(int i = -1; i<=1; i++)
            {
				for(int j = -1; j<=1; j++)
                {
					for(int k = -1; k<=1; k++)
                    {
						cx = ((xx + i) % nbrOfCells + nbrOfCells) % nbrOfCells;
						cy = ((yy + j) % nbrOfCells + nbrOfCells) % nbrOfCells;
						cz = ((zz + k) % nbrOfCells + nbrOfCells) % nbrOfCells;

						if (cellGrid[cx, cy, cz].getStatus() != ALIVE)
						{
							uncountNeighbourhood(cx, cy, cz);
							cellGrid[cx, cy, cz].changeStatus(ALIVE);
							population++;
							addcountNeighbourhood(cx, cy, cz);
							statusTexture.SetPixel(cx + nbrOfCells * cy, cz, new Color(ALIVE, 0, 0, 0));
						}
					}
                }
            }
			
		}

		statusTexture.Apply();
	}

	private void parametersUpdate()
    {
		for (int i = 0; i < nbrOfCells * nbrOfCells * nbrOfCells; i++)
			visited[i] = false;

		deltaPop =(int) (population * (lambda * (1 - population /(double) (nbrOfCells * nbrOfCells * nbrOfCells)) - 1));
		alpha = (deltaPop - birthDead) / (double)(sustainDead + deathzoneDead);
		if (alpha < 0)
			alpha = 0;
		pAdd = deltaPop / (birthDead + alpha * (sustainDead + deathzoneDead));

		beta = (-deltaPop - deathzoneAlive) / (double)(sustainAlive + birthAlive);
		if (beta < 0)
			beta = 0;
		pMinus = -deltaPop / (deathzoneAlive + beta * (sustainAlive + birthAlive));

		p = population / (double)(nbrOfCells * nbrOfCells * nbrOfCells);
		deltap = deltaPop / (double)(nbrOfCells * nbrOfCells * nbrOfCells);
    }

	private void uncountNeighbourhood(int x, int y, int z)
    {
		foreach(int n in getNeighbourhood(x, y, z))
        {
			switch (getKind(n))
			{
				case Kinds.BTH_A:
					birthAlive--;
					break;
				case Kinds.BTH_D:
					birthDead--;
					break;
				case Kinds.DTH_A:
					deathzoneAlive--;
					break;
				case Kinds.DTH_D:
					deathzoneDead--;
					break;
				case Kinds.STN_A:
					sustainAlive--;
					break;
				case Kinds.STN_D:
					sustainDead--;
					break;
				default:
					break;
			}
		}
    }

	private void addcountNeighbourhood(int x, int y, int z)
	{
		foreach (int n in getNeighbourhood(x, y, z))
		{
			switch (getKind(n))
			{
				case Kinds.BTH_A:
					birthAlive++;
					break;
				case Kinds.BTH_D:
					birthDead++;
					break;
				case Kinds.DTH_A:
					deathzoneAlive++;
					break;
				case Kinds.DTH_D:
					deathzoneDead++;
					break;
				case Kinds.STN_A:
					sustainAlive++;
					break;
				case Kinds.STN_D:
					sustainDead++;
					break;
				default:
					break;
			}
		}
	}

}
