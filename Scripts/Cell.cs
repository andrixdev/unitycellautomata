
using System.Runtime.InteropServices;

class Cell
{
	private int status;
	private int xNbr, yNbr, zNbr;
	private int lifespan;

	//Accesseurs et mutateurs
	public Cell(int xNbr, int yNbr, int zNbr, float cellLength, int status)
	{
		this.xNbr = xNbr;
		this.yNbr = yNbr;
		this.zNbr = zNbr;
		this.status = status;
		this.lifespan = 0;
		//this.transform.position = Vector3.right*(xNbr*cellLength + cellLength/2f) + Vector3.up*(yNbr*cellLength + cellLength/2) + Vector3.forward*(zNbr*cellLength + cellLength/2);
	}

	public int getStatus()
	{
		return status;
	}

	public void changeStatus(int new_status)
	{
		this.status = new_status;
	}

	public int getLifespan()
	{
		return lifespan;
	}

	public void incrLifespan()
    {
		this.lifespan++;
    }

	public void resetLifespan()
	{
		this.lifespan = 0;
	}

}
